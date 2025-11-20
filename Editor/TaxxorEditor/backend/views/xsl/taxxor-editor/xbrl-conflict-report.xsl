<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:param name="base-uri"/>
    <xsl:param name="project-id"/>
    <xsl:param name="project-name"/>
    <xsl:param name="outputchannel-id"/>
    <xsl:param name="outputchannel-name"/>
    <xsl:param name="targetmodel-id"/>
    <xsl:param name="targetmodel-name"/>
    <xsl:param name="timestamp"/>




    <xsl:template match="/">
        <html>
            <head>
                <title>XBRL conflict report</title>
                <link href="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.3.7/css/bootstrap.css" rel="stylesheet" type="text/css"/>
                <style>
                    body {
                        margin: 10px;
                    }
                    h2 {
                        font-size: 18px;
                    }
                    h3 {
                        font-size: 16px;
                    }
                    .col1 {
                        width: 5%;
                    }
                    .col2 {
                        width: 55%;
                    }
                    .col3 {
                        width: 35%;
                    }
                    .col4 {
                        width: 5%;
                    }
                    td {
                        vertical-align: baseline;
                    }</style>
            </head>
            <body>
                <h1>XBRL conflict report</h1>
                <xsl:choose>
                    <xsl:when test="count(/report/conflict) = 0">
                        <h2>No conflicts found</h2>
                    </xsl:when>
                    <xsl:otherwise>
                        <h2>Total conflicts <code><xsl:value-of select="count(/report/conflict)"/></code></h2>
                        <br/>
                        <br/>
                        <h3>Conflict details</h3>
                        <xsl:apply-templates select="/report/conflict"/>
                    </xsl:otherwise>
                </xsl:choose>
                <br/>
                <br/>
                <h3>Collision report information</h3>
                <table class="table table-condensed">
                    <tbody>
                        <tr>
                            <th scope="row" style="width: 30%;">Timestamp</th>
                            <td style="width: 70%;">
                                <xsl:value-of select="$timestamp"/>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">Project</th>
                            <td>
                                <xsl:value-of select="$project-name"/>
                                <xsl:text> </xsl:text>
                                <small class="text-muted">(<xsl:value-of select="$project-id"/>)</small>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">Output channel</th>
                            <td>
                                <xsl:value-of select="$outputchannel-name"/>
                                <xsl:text> </xsl:text>
                                <small class="text-muted">(<xsl:value-of select="$outputchannel-id"/>)</small>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">Model</th>
                            <td>
                                <xsl:value-of select="$targetmodel-name"/>
                                <xsl:text> </xsl:text>
                                <small class="text-muted">(<xsl:value-of select="$targetmodel-id"/>)</small>
                            </td>
                        </tr>
                    </tbody>
                </table>

            </body>
        </html>
    </xsl:template>

    <xsl:template match="conflict">
        <xsl:variable name="period-end" select="substring-before(@periodEnd, 'T')"/>
        <table class="table table-condensed">
            <caption>
                <xsl:value-of select="@target"/>
                <br/>
                <small class="text-muted">
                    <xsl:choose>
                        <xsl:when test="@periodStart">
                            <xsl:variable name="period-start" select="substring-before(@periodStart, 'T')"/>
                            <xsl:value-of select="concat('(', $period-start, ' - ', $period-end, ')')"/>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="concat('(', $period-end, ')')"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </small>
            </caption>
            <thead>
                <tr>
                    <th class="col1">#</th>
                    <th class="col2">Section</th>
                    <th class="col3">Displayed value</th>
                    <th class="col4">Link</th>
                </tr>
            </thead>
            <tbody>
                <xsl:apply-templates select="mapping"/>
            </tbody>
        </table>
    </xsl:template>

    <xsl:template match="mapping">

        <tr>
            <th scope="row">
                <xsl:value-of select="position()"/>
            </th>
            <td>
                <xsl:choose>
                    <xsl:when test="not(@sectionName = 'unknown')">
                        <xsl:if test="@sectionNumber">
                            <xsl:value-of select="@sectionNumber"/>
                            <xsl:text> - </xsl:text>
                        </xsl:if>
                        <xsl:value-of select="@sectionName"/>
                        <br/>
                        <small class="text-muted">
                            <xsl:text>(</xsl:text>
                            <xsl:value-of select="@section"/>
                            <xsl:text>)</xsl:text>
                        </small>
                    </xsl:when>
                    <xsl:otherwise>
                        <xsl:value-of select="@section"/>
                    </xsl:otherwise>
                </xsl:choose>
            </td>
            <td>
                <xsl:value-of select="@formattedValue"/>
                <br/>
                <small class="text-muted">
                    <xsl:choose>
                        <xsl:when test="string-length(@textValue) = 0">
                            <xsl:value-of select="concat('(value: ', @value, ')')"/>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="concat('(value: ', @value, ', text: ', @textValue, ')')"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </small>
            </td>
            <td>
                <xsl:choose>
                    <xsl:when test="@status = 'ok'">
                        <a href="#" target="_blank">
                            <xsl:attribute name="href">
                                <xsl:call-template name="render-link">
                                    <xsl:with-param name="item-id" select="@sectionId"/>
                                    <xsl:with-param name="fact-id" select="@factId"/>
                                </xsl:call-template>
                            </xsl:attribute>
                            <xsl:text>open</xsl:text>
                        </a>
                    </xsl:when>
                    <xsl:when test="@status = 'noid'">
                        <xsl:text>-- not item id -</xsl:text>
                    </xsl:when>
                    <xsl:when test="@status = 'notinuse'">
                        <xsl:text>-- not in use --</xsl:text>
                    </xsl:when>
                    <xsl:when test="@status = 'nodataref'">
                        <xsl:text>-- no datareference --</xsl:text>
                    </xsl:when>
                    <xsl:otherwise>
                        <xsl:text>Unknown status: </xsl:text>
                        <xsl:value-of select="@status"/>
                    </xsl:otherwise>
                </xsl:choose>

            </td>
        </tr>
    </xsl:template>

    <xsl:template name="render-link">
        <xsl:param name="item-id"/>
        <xsl:param name="fact-id"/>
        <xsl:value-of select="concat($base-uri, '/report_editors/default/editor.html?pid=', $project-id, '&amp;did=foobar&amp;ocvariantid=', $outputchannel-id, '#did=', $item-id, '&amp;factid=', $fact-id)"/>
    </xsl:template>


</xsl:stylesheet>

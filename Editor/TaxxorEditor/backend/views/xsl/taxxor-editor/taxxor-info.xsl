<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:param name="type">status</xsl:param>
    <xsl:param name="displaydetails">no</xsl:param>

    <xsl:output method="xml" omit-xml-declaration="yes" indent="yes"/>


    <xsl:template match="/">
        <div class="info {$type}">
            <xsl:choose>
                <xsl:when test="$type = 'about'">
                    <xsl:apply-templates select="/configuration/repositories/taxxoreditor"/>
                </xsl:when>
                <xsl:when test="$type = 'status'">
                    <dl>
                        <xsl:apply-templates select="/configuration/taxxor/components/service"/>
                    </dl>
                </xsl:when>
                <xsl:otherwise>
                    <p>Unknown type: <xsl:value-of select="$type"/></p>
                </xsl:otherwise>
            </xsl:choose>
        </div>

    </xsl:template>
    
    <xsl:template match="service">
        <dt>
            <xsl:text>Taxxor Reverse Proxy</xsl:text>
        </dt>
        <dd>
            <xsl:text>Status: </xsl:text>
            <span class="ok">OK</span>
        </dd>
        <xsl:apply-templates select="web-applications/web-application" mode="post-proxy"/>
        <xsl:apply-templates select="services/service" mode="post-proxy"/>
    </xsl:template>
    
    <xsl:template match="web-application | service" mode="post-proxy">
        <dt>
            <!-- Render the service name -->
            <xsl:value-of select="@name"/>
        </dt>
        <dd>
            <xsl:text>Status: </xsl:text>
            <span>
                <xsl:attribute name="class">
                    <xsl:choose>
                        <xsl:when test="@status='OK'">ok</xsl:when>
                        <xsl:otherwise>error</xsl:otherwise>
                    </xsl:choose>
                </xsl:attribute>
                <xsl:value-of select="@status"/>
                <xsl:if test="$displaydetails='yes'">
                    <xsl:text> </xsl:text>
                    <small>(uri: <xsl:value-of select="uri"/>)</small>
                </xsl:if>
            </span>
        </dd>
    </xsl:template>

    <xsl:template match="taxxoreditor">
        <div class="core-components">
            <h5>Core</h5>
            <ul class="list-unstyled">
                <xsl:apply-templates select="repro[@id='root']"/>
            </ul>
        </div>
        <div class="other-components">
            <h5>Modules and plugins</h5>
            <ul>
                <xsl:apply-templates select="repro[not(@id='root')]"/>
            </ul>
        </div>
        <div class="more-components">
            <h5>Other</h5>
            <ul>
                <li>Taxxor Excel Plugin <code>v1.0</code> - <a href="https://editor.taxxor.taxxordm.com/public/downloads/TaxxorExcelAddInSetup.msi">Download</a></li>
            </ul>
        </div>
    </xsl:template>
    
    <xsl:template match="repro">
        <li>
            <xsl:value-of select="name"/>
            <xsl:text>: </xsl:text>
            <code>
                <xsl:text>v</xsl:text>
                <xsl:value-of select="location/@version"/>
            </code>
            <xsl:if test="submodules">
                <ul>
                    <xsl:apply-templates select="submodules/location"/>  
                </ul>
            </xsl:if>
        </li>
    </xsl:template>
    
    <xsl:template match="location">
        <li>
            <xsl:value-of select="@name"/>
            <xsl:text>: </xsl:text>
            <code>
                <xsl:text>v</xsl:text>
                <xsl:value-of select="@version"/>
            </code>               
        </li>
    </xsl:template>

    <xsl:template match="editor">
        <xsl:variable name="editor-id" select="@id"/>
        <li>
            <xsl:value-of select="name"/>
            <xsl:text>: </xsl:text>
            <code>
                <xsl:text>v</xsl:text>
                <xsl:choose>
                    <xsl:when test="string-length(normalize-space(@version)) &gt; 0"><xsl:value-of select="@version"/></xsl:when>
                    <xsl:otherwise>
                        <!-- Retrieve the version from the cache -->
                        <xsl:value-of select="/configuration/repros/editors/editor[@id=$editor-id]/@version"/>
                    </xsl:otherwise>
                </xsl:choose>
                
            </code>               
        </li>
    </xsl:template>

</xsl:stylesheet>

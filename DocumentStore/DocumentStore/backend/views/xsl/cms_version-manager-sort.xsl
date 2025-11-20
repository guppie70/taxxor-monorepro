<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">

    <xsl:output method="xml" omit-xml-declaration="no" indent="no" encoding="UTF-8"/>

    <xsl:template match="@* | node()">
        <xsl:copy>
            <xsl:apply-templates select="@* | node()"/>
        </xsl:copy>
    </xsl:template>

    <!-- for auditor view -->
    <xsl:template match="tags">
        <tags>
            <xsl:for-each select="tag-meta[@repro = 'project-data']">
                <xsl:sort select="tag/date/@epoch" order="descending" data-type="number"/>
                
                <!-- Retrieve corresponding content repository data -->
                <xsl:variable name="current-tagname" select="@tagname"/>
                <xsl:variable name="content-commit-hash">
                    <xsl:choose>
                        <xsl:when test="/tags/tag-meta[@repro = 'project-content' and @tagname = $current-tagname]">
                            <xsl:value-of select="/tags/tag-meta[@repro = 'project-content' and @tagname = $current-tagname]/@hash"/>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:text>none</xsl:text>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:variable>

                <tag name="{@tagname}" hashData="{@hash}" hashContent="{$content-commit-hash}">
                    <xsl:apply-templates select="*/*"/>
                </tag>

            </xsl:for-each>
        </tags>
    </xsl:template>
</xsl:stylesheet>
